import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Controller, useFieldArray, useForm, type FieldPath } from 'react-hook-form'
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom'
import { useEffect, useMemo } from 'react'
import { z } from 'zod'
import {
  ApiError,
  createProduct,
  getAttributeDefinitions,
  getCategories,
  getProduct,
  updateProduct,
} from '../api/client'

const variantSchema = z.object({
  sku: z.string().min(1, 'SKU is required').max(64),
  size: z.string().min(1, 'Size is required').max(50),
  color: z.string().min(1, 'Color is required').max(50),
  price: z.coerce.number().min(0, 'Price must be >= 0'),
  stockQuantity: z.coerce.number().int().min(0, 'Stock must be >= 0'),
})

const attributeSchema = z.object({
  attributeDefinitionId: z.string().min(1, 'Attribute is required'),
  value: z.string().min(1, 'Value is required').max(500),
})

const formSchema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  description: z.string().max(2000).optional().or(z.literal('')),
  categoryId: z.string().min(1, 'Category is required'),
  variants: z.array(variantSchema).min(1, 'At least one variant is required'),
  attributes: z.array(attributeSchema),
})

type FormValues = z.infer<typeof formSchema>

const formRoots = new Set(['name', 'description', 'categoryId', 'variants', 'attributes'])

function toFormField(key: string): FieldPath<FormValues> | null {
  const camel = key.charAt(0).toLowerCase() + key.slice(1)
  const path = camel.replace(/\[(\d+)\]/g, '.$1')
  const root = path.split('.')[0]
  return formRoots.has(root) ? (path as FieldPath<FormValues>) : null
}

const emptyVariant = {
  sku: '',
  size: '',
  color: '',
  price: 0,
  stockQuantity: 0,
}

export function ProductFormPage() {
  const { id } = useParams()
  const isEdit = Boolean(id)
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const categoriesQuery = useQuery({ queryKey: ['categories'], queryFn: getCategories })
  const attributesQuery = useQuery({
    queryKey: ['attribute-definitions'],
    queryFn: getAttributeDefinitions,
  })
  const productQuery = useQuery({
    queryKey: ['product', id],
    queryFn: () => getProduct(id!),
    enabled: isEdit,
  })

  const {
    control,
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      name: '',
      description: '',
      categoryId: '',
      variants: [emptyVariant],
      attributes: [],
    },
  })

  const variantsArray = useFieldArray({ control, name: 'variants' })
  const attributesArray = useFieldArray({ control, name: 'attributes' })

  useEffect(() => {
    if (!productQuery.data) return
    reset({
      name: productQuery.data.name,
      description: productQuery.data.description ?? '',
      categoryId: productQuery.data.categoryId,
      variants: productQuery.data.variants.map((v) => ({
        sku: v.sku,
        size: v.size,
        color: v.color,
        price: v.price,
        stockQuantity: v.stockQuantity,
      })),
      attributes: productQuery.data.attributes.map((a) => ({
        attributeDefinitionId: a.attributeDefinitionId,
        value: a.value,
      })),
    })
  }, [productQuery.data, reset])

  const saveMutation = useMutation({
    mutationFn: async (values: FormValues) => {
      const payload = {
        name: values.name,
        description: values.description || null,
        categoryId: values.categoryId,
        variants: values.variants,
        attributes: values.attributes,
        rowVersion: productQuery.data?.rowVersion,
      }
      if (isEdit && id) {
        return updateProduct(id, payload)
      }
      return createProduct(payload)
    },
    onSuccess: async (product) => {
      await queryClient.invalidateQueries({ queryKey: ['products'] })
      await queryClient.invalidateQueries({ queryKey: ['product', product.id] })
      navigate('/')
    },
    onError: (error: unknown) => {
      if (!(error instanceof ApiError)) return
      if (error.errors) {
        Object.entries(error.errors).forEach(([key, messages]) => {
          const field = toFormField(key)
          if (field) {
            setError(field, { message: messages[0] })
          }
        })
      }
    },
  })

  const serverAlert = useMemo(() => {
    const error = saveMutation.error
    if (!(error instanceof ApiError)) return null
    if (error.status === 409) {
      if (error.title.toLowerCase().includes('sku')) {
        return 'Duplicate SKU. Choose a unique SKU and try again.'
      }
      return 'Concurrency conflict. Another user updated this product. Refresh and try again.'
    }
    return error.title
  }, [saveMutation.error])

  if (isEdit && productQuery.isLoading) {
    return (
      <Box display="flex" justifyContent="center" py={6}>
        <CircularProgress />
      </Box>
    )
  }

  if (isEdit && productQuery.isError) {
    return (
      <Alert severity="error">
        {productQuery.error instanceof ApiError
          ? productQuery.error.title
          : 'Failed to load product.'}
      </Alert>
    )
  }

  return (
    <Stack spacing={2} component="form" onSubmit={handleSubmit((v) => saveMutation.mutate(v))}>
      <Stack direction="row" justifyContent="space-between" alignItems="center">
        <Typography variant="h4" component="h1">
          {isEdit ? 'Edit product' : 'Create product'}
        </Typography>
        <Button component={RouterLink} to="/">
          Back to list
        </Button>
      </Stack>

      {serverAlert && <Alert severity="error">{serverAlert}</Alert>}

      <Paper sx={{ p: 2 }}>
        <Stack spacing={2}>
          <TextField
            label="Name"
            {...register('name')}
            error={Boolean(errors.name)}
            helperText={errors.name?.message}
            fullWidth
          />
          <TextField
            label="Description"
            {...register('description')}
            error={Boolean(errors.description)}
            helperText={errors.description?.message}
            fullWidth
            multiline
            minRows={2}
          />
          <FormControl fullWidth error={Boolean(errors.categoryId)}>
            <InputLabel id="category-label">Category</InputLabel>
            <Controller
              name="categoryId"
              control={control}
              render={({ field }) => (
                <Select labelId="category-label" label="Category" {...field}>
                  {(categoriesQuery.data ?? []).map((c) => (
                    <MenuItem key={c.id} value={c.id}>
                      {c.name}
                    </MenuItem>
                  ))}
                </Select>
              )}
            />
            {errors.categoryId && <FormHelperText>{errors.categoryId.message}</FormHelperText>}
          </FormControl>
        </Stack>
      </Paper>

      <Paper sx={{ p: 2 }}>
        <Stack spacing={2}>
          <Stack direction="row" justifyContent="space-between" alignItems="center">
            <Typography variant="h6">Variants</Typography>
            <Button type="button" onClick={() => variantsArray.append(emptyVariant)}>
              Add variant
            </Button>
          </Stack>
          {errors.variants?.root && (
            <Alert severity="error">{errors.variants.root.message}</Alert>
          )}
          {typeof errors.variants?.message === 'string' && (
            <Alert severity="error">{errors.variants.message}</Alert>
          )}
          {variantsArray.fields.map((field, index) => (
            <Stack
              key={field.id}
              direction={{ xs: 'column', md: 'row' }}
              spacing={1}
              alignItems={{ md: 'flex-start' }}
            >
              <TextField
                label="SKU"
                {...register(`variants.${index}.sku`)}
                error={Boolean(errors.variants?.[index]?.sku)}
                helperText={errors.variants?.[index]?.sku?.message}
                fullWidth
              />
              <TextField
                label="Size"
                {...register(`variants.${index}.size`)}
                error={Boolean(errors.variants?.[index]?.size)}
                helperText={errors.variants?.[index]?.size?.message}
                fullWidth
              />
              <TextField
                label="Color"
                {...register(`variants.${index}.color`)}
                error={Boolean(errors.variants?.[index]?.color)}
                helperText={errors.variants?.[index]?.color?.message}
                fullWidth
              />
              <TextField
                label="Price"
                type="number"
                inputProps={{ step: '0.01', min: 0 }}
                {...register(`variants.${index}.price`)}
                error={Boolean(errors.variants?.[index]?.price)}
                helperText={errors.variants?.[index]?.price?.message}
                fullWidth
              />
              <TextField
                label="Stock"
                type="number"
                inputProps={{ min: 0 }}
                {...register(`variants.${index}.stockQuantity`)}
                error={Boolean(errors.variants?.[index]?.stockQuantity)}
                helperText={errors.variants?.[index]?.stockQuantity?.message}
                fullWidth
              />
              <Button
                type="button"
                color="error"
                disabled={variantsArray.fields.length === 1}
                onClick={() => variantsArray.remove(index)}
              >
                Remove
              </Button>
            </Stack>
          ))}
        </Stack>
      </Paper>

      <Paper sx={{ p: 2 }}>
        <Stack spacing={2}>
          <Stack direction="row" justifyContent="space-between" alignItems="center">
            <Typography variant="h6">Attributes</Typography>
            <Button
              type="button"
              onClick={() =>
                attributesArray.append({
                  attributeDefinitionId: attributesQuery.data?.[0]?.id ?? '',
                  value: '',
                })
              }
            >
              Add attribute
            </Button>
          </Stack>
          {errors.attributes?.root && (
            <Alert severity="error">{errors.attributes.root.message}</Alert>
          )}
          {typeof errors.attributes?.message === 'string' && (
            <Alert severity="error">{errors.attributes.message}</Alert>
          )}
          {attributesArray.fields.map((field, index) => (
            <Stack
              key={field.id}
              direction={{ xs: 'column', md: 'row' }}
              spacing={1}
              alignItems={{ md: 'flex-start' }}
            >
              <FormControl fullWidth error={Boolean(errors.attributes?.[index]?.attributeDefinitionId)}>
                <InputLabel id={`attr-label-${index}`}>Attribute</InputLabel>
                <Controller
                  name={`attributes.${index}.attributeDefinitionId`}
                  control={control}
                  render={({ field: selectField }) => (
                    <Select labelId={`attr-label-${index}`} label="Attribute" {...selectField}>
                      {(attributesQuery.data ?? []).map((a) => (
                        <MenuItem key={a.id} value={a.id}>
                          {a.name}
                        </MenuItem>
                      ))}
                    </Select>
                  )}
                />
              </FormControl>
              <TextField
                label="Value"
                {...register(`attributes.${index}.value`)}
                error={Boolean(errors.attributes?.[index]?.value)}
                helperText={errors.attributes?.[index]?.value?.message}
                fullWidth
              />
              <Button type="button" color="error" onClick={() => attributesArray.remove(index)}>
                Remove
              </Button>
            </Stack>
          ))}
        </Stack>
      </Paper>

      <Stack direction="row" spacing={2}>
        <Button type="submit" variant="contained" disabled={saveMutation.isPending}>
          {saveMutation.isPending ? 'Saving…' : 'Save'}
        </Button>
        <Button component={RouterLink} to="/" disabled={saveMutation.isPending}>
          Cancel
        </Button>
      </Stack>
    </Stack>
  )
}
